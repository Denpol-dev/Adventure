using Adventure.Entities;
using Adventure.Entities.Items;
using Adventure.Entities.Levels;
using Adventure.Entities.Maps;
using Adventure.Entities.Worlds;
using Foxnet.Session;

namespace Adventure.Expeditions;

internal sealed class ExpeditionMap
{
    private readonly StationDefinition _station;
    private readonly Dictionary<WorldPosition, string> _gates = [];
    private readonly Dictionary<WorldPosition, InteriorArea> _interiors = [];
    private readonly Dictionary<string, DroneDefinition> _drones;

    public ExpeditionMap(InitLevels levels, ExpeditionDefinition definition)
    {
        Levels = levels.Levels;
        _drones = definition.Drones.ToDictionary(drone => drone.Id);
        _station = definition.Station;
        var map = Levels[_station.Origin.LevelId].Map;
        for (int y = _station.Origin.Y; y < _station.Origin.Y + _station.Height; y++)
        for (int x = _station.Origin.X; x < _station.Origin.X + _station.Width; x++)
        {
            if (map[x, y].Objects.Any(obj => obj.Id == _station.ServiceTerminalId))
                Terminal = new WorldPosition(_station.Origin.LevelId, x, y);
        }

        foreach (var bay in _station.Bays)
            _gates.Add(_station.At(bay.GateX, bay.GateY), bay.DroneId);

        foreach (var room in definition.Interiors)
        for (int y = 0; y < room.Height; y++)
        for (int x = 0; x < room.Width; x++)
            _interiors.Add(room.Origin with { X = room.Origin.X + x, Y = room.Origin.Y + y }, room);
    }

    public IReadOnlyDictionary<Guid, Level> Levels { get; }
    public WorldPosition Terminal { get; }
    public bool OnStation(WorldPosition position) =>
        _station.Contains(position) && StationDefinition.IsStationGlyph(CellAt(position).Symbol);
    public bool IsValid(WorldPosition position) =>
        Levels.TryGetValue(position.LevelId, out var level) && level.Map.Contains(position.X, position.Y);

    public Cell CellAt(WorldPosition position) => Levels[position.LevelId].Map[position.X, position.Y];

    public string? Interior(WorldPosition position)
    {
        var properties = CellAt(position).Properties;
        return properties?.RoomId != null ? properties.BuildingId : _interiors.GetValueOrDefault(position)?.BuildingId;
    }

    public bool CanEnter(WorldPosition position, string droneId, IReadOnlyCollection<Item> inventory)
    {
        if (_gates.TryGetValue(position, out var assigned))
            return droneId == assigned;
        return CellAt(position).CanEnter(inventory);
    }

    public bool BlocksScan(WorldPosition position, bool roofProjection = false)
    {
        if (_gates.ContainsKey(position))
            return true;
        var cell = CellAt(position);
        return cell.Properties?.BlocksScanning ??
            (!roofProjection && (cell.Terrain is { IsCollision: true } || cell.Symbol == '◉' ||
                cell.Objects.Any(obj => obj.Actor.IsCollision)));
    }

    public ObservedCell Sample(WorldPosition position)
    {
        if (_gates.TryGetValue(position, out var owner))
            return new('≈', _drones[owner].Color, true);

        var cell = CellAt(position);
        return new(cell.Fill[0], cell.VisibleActor?.Color ?? ConsoleColor.Gray, true);
    }

    public int CellIndex(WorldPosition position) => position.Y * Levels[position.LevelId].Map.Width + position.X;

    public bool TryStep(WorldPosition position, Vector direction, out WorldPosition next)
    {
        var level = Levels[position.LevelId];
        var (dx, dy) = direction.Offset();
        int x = position.X + dx;
        int y = position.Y + dy;
        if (!level.Map.Contains(x, y))
        {
            var neighbor = level.GetNeighbor(direction);
            if (neighbor == null)
            {
                next = default;
                return false;
            }
            level = neighbor;
            if (dx != 0)
                x = dx < 0 ? level.Map.Width - 1 : 0;
            if (dy != 0)
                y = dy < 0 ? level.Map.Height - 1 : 0;
        }
        next = new WorldPosition(level.Id, x, y);
        return level.Map.Contains(x, y);
    }

    public bool TryOffset(WorldPosition start, int x, int y, out WorldPosition position)
    {
        position = start;
        var horizontal = x < 0 ? Vector.Left : Vector.Right;
        var vertical = y < 0 ? Vector.Top : Vector.Bottom;
        for (int step = 0; step < Math.Abs(x); step++)
        {
            if (!TryStep(position, horizontal, out position))
                return false;
        }
        for (int step = 0; step < Math.Abs(y); step++)
        {
            if (!TryStep(position, vertical, out position))
                return false;
        }
        return true;
    }
}
