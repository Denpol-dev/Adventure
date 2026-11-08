using Foxnet.Session;
using Microsoft.Data.Sqlite;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Channels;
using static Foxnet.Session.ExpeditionPipeClient;

namespace Adventure.Expeditions;

public sealed class ExpeditionPipeServer : IAsyncDisposable
{
    private readonly ExpeditionOwner _owner;
    private readonly string _name;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _server;

    public ExpeditionPipeServer(ExpeditionOwner owner, string databasePath)
    {
        _owner = owner;
        _name = ExpeditionAddress.PipeName(databasePath);
        _server = ServeAsync(_shutdown.Token);
    }

    private async Task ServeAsync(CancellationToken cancellationToken)
    {
        var clients = new List<Task>();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var pipe = new NamedPipeServerStream(_name, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                try
                {
                    await pipe.WaitForConnectionAsync(cancellationToken);
                }
                catch
                {
                    await pipe.DisposeAsync();
                    throw;
                }
                clients.RemoveAll(task => task.IsCompletedSuccessfully);
                clients.Add(HandleAsync(pipe, cancellationToken));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            await Task.WhenAll(clients);
        }
    }

    private async Task HandleAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        await using var connection = pipe;
        using var reader = CreateReader(pipe);
        await using var writer = CreateWriter(pipe);
        try
        {
            if (await ReadAsync<ExpeditionRequest>(reader, cancellationToken) is not { } request)
                return;

            if (request.Kind == ExpeditionRequestKind.Watch)
            {
                await WatchAsync(reader, writer, cancellationToken);
                return;
            }

            ExpeditionReply reply;
            if (request.Kind is ExpeditionRequestKind.Initialize or ExpeditionRequestKind.Status)
                reply = new ExpeditionReply(_owner.Status());
            else if (request is { Kind: ExpeditionRequestKind.Command, Command: { } command })
            {
                var (result, status) = _owner.Execute(command, remote: true);
                reply = new ExpeditionReply(status, result.Reply);
            }
            else
                reply = new ExpeditionReply(null, Error: "Неизвестный запрос экспедиции.");

            await WriteAsync(writer, reply, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or SqliteException or InvalidOperationException)
        {
            try
            {
                await WriteAsync(writer, new ExpeditionReply(null, Error: exception.Message), cancellationToken);
            }
            catch (Exception writeError) when (writeError is IOException or OperationCanceledException)
            {
            }
        }
    }

    private async Task WatchAsync(StreamReader reader, StreamWriter writer, CancellationToken cancellationToken)
    {
        using var watch = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var updates = Channel.CreateBounded<ExpeditionStatus>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });
        void Notify(ExpeditionStatus status) => updates.Writer.TryWrite(status);

        _owner.Changed += Notify;
        try
        {
            var initial = _owner.Status();
            long revision = initial.Revision;
            await WriteAsync(writer, new ExpeditionReply(initial), watch.Token);

            var disconnected = reader.ReadLineAsync(watch.Token).AsTask();
            while (true)
            {
                var available = updates.Reader.WaitToReadAsync(watch.Token).AsTask();
                if (await Task.WhenAny(available, disconnected) == disconnected)
                {
                    await disconnected;
                    break;
                }
                if (!await available)
                    break;
                if (updates.Reader.TryRead(out var status) && status.Revision > revision)
                {
                    await WriteAsync(writer, new ExpeditionReply(status), watch.Token);
                    revision = status.Revision;
                }
            }
        }
        finally
        {
            watch.Cancel();
            _owner.Changed -= Notify;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        try
        {
            await _server;
        }
        finally
        {
            _shutdown.Dispose();
        }
    }
}
