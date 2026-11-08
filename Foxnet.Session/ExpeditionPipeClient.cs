using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Foxnet.Session;

public sealed class ExpeditionPipeClient(string databasePath)
{
    private readonly string _pipe = ExpeditionAddress.PipeName(databasePath);

    public async Task<ExpeditionReply> RequestAsync(ExpeditionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Kind == ExpeditionRequestKind.Watch)
            throw new ArgumentException("Для подписки используйте WatchAsync.", nameof(request));
        await using var pipe = await ConnectAsync(cancellationToken);
        await using var writer = CreateWriter(pipe);
        using var reader = CreateReader(pipe);
        await WriteAsync(writer, request, cancellationToken);
        return await ReadAsync<ExpeditionReply>(reader, cancellationToken)
            ?? throw new EndOfStreamException("Консоль закрыла соединение.");
    }

    public async IAsyncEnumerable<ExpeditionStatus> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var pipe = await ConnectAsync(cancellationToken);
        await using var writer = CreateWriter(pipe);
        using var reader = CreateReader(pipe);
        await WriteAsync(writer, new ExpeditionRequest(ExpeditionRequestKind.Watch), cancellationToken);
        while (await ReadAsync<ExpeditionReply>(reader, cancellationToken) is { } reply)
        {
            if (reply.Status == null)
                throw new InvalidDataException(reply.Error ?? "Консоль не вернула состояние.");
            yield return reply.Status;
        }
    }

    private async Task<NamedPipeClientStream> ConnectAsync(CancellationToken cancellationToken)
    {
        var pipe = new NamedPipeClientStream(".", _pipe, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await pipe.ConnectAsync(750, cancellationToken);
            return pipe;
        }
        catch
        {
            await pipe.DisposeAsync();
            throw;
        }
    }

    public static StreamReader CreateReader(Stream stream) => new(stream, Encoding.UTF8, leaveOpen: true);

    public static StreamWriter CreateWriter(Stream stream) =>
        new(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

    public static Task WriteAsync<T>(StreamWriter writer, T message, CancellationToken cancellationToken) =>
        writer.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), cancellationToken);

    public static async Task<T?> ReadAsync<T>(StreamReader reader, CancellationToken cancellationToken) where T : class
    {
        string? line = await reader.ReadLineAsync(cancellationToken);
        return line == null ? null : JsonSerializer.Deserialize<T>(line)
            ?? throw new InvalidDataException("Пустой пакет обмена с экспедицией.");
    }
}
