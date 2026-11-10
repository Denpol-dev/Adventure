using Adventure.Entities.Worlds;
using Foxnet.Session;
using System.Text.Json;

namespace Adventure.Expeditions;

public sealed class ExpeditionOwner : IDisposable
{
    private readonly object _gate = new();
    private readonly WorldDefinition _world;
    private readonly ExpeditionStore _store;
    private ExpeditionEngine _engine;
    private byte[] _saved;
    private long _revision;
    private bool _menuOpen;

    public ExpeditionOwner(WorldDefinition world, string databasePath)
    {
        _world = world;
        _store = new ExpeditionStore(databasePath);
        try
        {
            var existing = _store.Load(world.Id);
            if (existing != null)
            {
                WasRestored = true;
                var state = ReadState(existing.Payload);
                int previousLayout = state.LayoutRevision;
                bool legacyCargo = state.Drones.Values.Any(drone => drone.LegacyItems != null);
                _engine = new ExpeditionEngine(_world, state);
                _saved = existing.Payload;
                _revision = existing.Revision;
                if (state.LayoutRevision != previousLayout || legacyCargo)
                    SaveCurrentState();
            }
            else
            {
                _engine = new ExpeditionEngine(world);
                _saved = JsonSerializer.SerializeToUtf8Bytes(_engine.State);
                _revision = _store.Save(world.Id, 0, _saved);
            }
        }
        catch
        {
            _store.Dispose();
            throw;
        }
    }

    internal event Action<ExpeditionStatus>? Changed;

    public bool WasRestored { get; }

    public long Revision
    {
        get { lock (_gate) return _revision; }
    }

    public TResult Read<TResult>(Func<ExpeditionEngine, TResult> read)
    {
        lock (_gate)
            return read(_engine);
    }

    public ExpeditionStatus Status()
    {
        lock (_gate)
            return _engine.Status(_revision);
    }

    internal void SetMenuOpen(bool open)
    {
        lock (_gate)
            _menuOpen = open;
    }

    public (EngineResult Result, ExpeditionStatus Status) Execute(FlightCommand command, bool remote = false)
    {
        lock (_gate)
        {
            if (remote && _menuOpen)
                return (new EngineResult(new FlightResult(false, "Открыто меню управления.")), _engine.Status(_revision));

            EngineResult result;
            try
            {
                result = _engine.Apply(command);
                if (result.Reply.Applied)
                {
                    SaveCurrentState(result.Export);
                }
            }
            catch
            {
                _engine = new ExpeditionEngine(_world, ReadState(_saved));
                throw;
            }

            var status = _engine.Status(_revision);
            if (result.Reply.Applied)
                Changed?.Invoke(status);
            return (result, status);
        }
    }

    private void SaveCurrentState(ExpeditionExport? export = null)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(_engine.State);
        _revision = _store.Save(_world.Id, _revision, payload, export);
        _saved = payload;
    }

    private static ExpeditionState ReadState(byte[] payload)
    {
        try
        {
            return JsonSerializer.Deserialize<ExpeditionState>(payload)
                ?? throw new InvalidDataException("Сохранение экспедиции пустое.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Сохранение экспедиции повреждено.", exception);
        }
    }

    public void Dispose() => _store.Dispose();
}
