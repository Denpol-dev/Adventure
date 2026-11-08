using Adventure.Entities.Worlds;
using Foxnet.Session;

namespace Adventure.Expeditions;

internal static class ExpeditionValidator
{
    public static void Validate(ExpeditionDefinition definition, IReadOnlyDictionary<Guid, LevelDefinition> levels,
        IReadOnlyDictionary<string, HashSet<string>> buildings)
    {
        var balance = definition.Balance;
        Require(balance is { ScanRadius: > 0 and <= 40, WheelMove: > 0, LegMove: > 0, LidarScan: > 0, ToolUse: > 0,
            StationCharge: > 0, BatteryCapacity: > 0, HighCapacityBatteryCapacity: > 0 }, "Некорректный баланс экспедиции.");
        Require(definition.Drones is { Count: > 0 and <= 16 } && definition.Donors != null && definition.Interiors != null,
            "Некорректный парк экспедиции.");

        var station = definition.Station;
        Require(station != null && !string.IsNullOrWhiteSpace(station.Id) && station.Rows is { Count: > 0 } &&
            station.Bays != null, "Не задана ремонтная станция.");
        int width = station.Rows[0]?.Length ?? 0;
        Require(width > 0 && station.Rows.All(row => row != null && row.Length == width &&
            row.All(glyph => glyph is ' ' or '▢' or '+' or '-' or '|' or 'G' or 'T')) &&
            station.Rows.Sum(row => row.Count(glyph => glyph == 'T')) == 1 &&
            station.Rows.Sum(row => row.Count(glyph => glyph == 'G')) == station.Bays.Count,
            "Некорректная планировка ремонтной станции.");
        Require(Fits(station.Origin, width, station.Rows.Count, levels), "Ремонтная станция выходит за пределы квадранта.");
        Require(!levels[station.Origin.LevelId].Objects.Any(obj => OnStation(station.Origin with { X = obj.X, Y = obj.Y })),
            "Ремонтная станция перекрывает существующий объект.");

        var drones = new Dictionary<string, DroneDefinition>(StringComparer.Ordinal);
        var colors = new HashSet<ConsoleColor>();
        var occupied = new HashSet<WorldPosition>();
        var components = new HashSet<string>(StringComparer.Ordinal);
        foreach (var drone in definition.Drones)
        {
            Require(drone != null && !string.IsNullOrWhiteSpace(drone.Id) && !string.IsNullOrWhiteSpace(drone.Name) &&
                drones.TryAdd(drone.Id, drone) && Enum.IsDefined(drone.Kind) && Enum.IsDefined(drone.Color) &&
                colors.Add(drone.Color) && drone.MaxWeight > 0 && drone.BatteryPercent is > 0 and <= 100 &&
                IsWalkable(drone.Position) && occupied.Add(drone.Position), "Некорректное описание управляемого дрона.");
            foreach (var slot in new[] { EquipmentSlot.Locomotion, EquipmentSlot.LeftArm, EquipmentSlot.RightArm, EquipmentSlot.Battery })
                components.Add($"{drone.Id}/{slot}");
        }
        Require(drones.ContainsKey(definition.StartDroneId) && station.Bays.Count == drones.Count,
            "Начальный дрон или парковочные боксы не заданы.");

        var bayIds = new HashSet<string>(StringComparer.Ordinal);
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        var gates = new HashSet<(int, int)>();
        var parking = new HashSet<(int, int)>();
        foreach (var bay in station.Bays)
        {
            Require(bay != null && !string.IsNullOrWhiteSpace(bay.Id) && bayIds.Add(bay.Id) &&
                drones.ContainsKey(bay.DroneId) && assigned.Add(bay.DroneId) &&
                Glyph(bay.GateX, bay.GateY) == 'G' && gates.Add((bay.GateX, bay.GateY)) &&
                Glyph(bay.ParkingX, bay.ParkingY) == '▢' && parking.Add((bay.ParkingX, bay.ParkingY)),
                "Некорректный парковочный бокс.");
            var drone = drones[bay.DroneId];
            var parked = station.Origin with { X = station.Origin.X + bay.ParkingX, Y = station.Origin.Y + bay.ParkingY };
            Require(drone.Id == definition.StartDroneId ? drone.Position == parked : !OnStation(drone.Position),
                $"Некорректная начальная позиция дрона {drone.Id}.");
        }

        var donorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var donor in definition.Donors)
        {
            Require(donor != null && !string.IsNullOrWhiteSpace(donor.Id) && donorIds.Add(donor.Id) &&
                !drones.ContainsKey(donor.Id) && !string.IsNullOrWhiteSpace(donor.Name) &&
                IsWalkable(donor.Position) && occupied.Add(donor.Position) && donor.Components != null,
                "Некорректное описание донора.");
            foreach (var module in donor.Components)
            {
                Require(module != null && !string.IsNullOrWhiteSpace(module.Id) && components.Add(module.Id) &&
                    Enum.IsDefined(module.Kind) && module.BatteryPercent is >= 0 and <= 100 &&
                    (module.BatteryPercent == 0 || Equipment.Capacity(module.Kind, balance) > 0),
                    $"Некорректный модуль донора {donor.Id}.");
            }
        }

        var rooms = new HashSet<WorldPosition>();
        foreach (var interior in definition.Interiors)
        {
            Require(interior != null && buildings.TryGetValue(interior.BuildingId, out var building) &&
                building.Contains(interior.RoomId) && Fits(interior.Origin, interior.Width, interior.Height, levels),
                "Некорректная область помещения.");
            for (int y = 0; y < interior.Height; y++)
            for (int x = 0; x < interior.Width; x++)
            {
                var point = interior.Origin with { X = interior.Origin.X + x, Y = interior.Origin.Y + y };
                Require(!OnStation(point) && rooms.Add(point), "Области помещений пересекаются.");
            }
        }

        char Glyph(int x, int y) => x >= 0 && y >= 0 && x < width && y < station.Rows.Count ? station.Rows[y][x] : ' ';
        bool OnStation(WorldPosition position) => position.LevelId == station.Origin.LevelId &&
            Glyph(position.X - station.Origin.X, position.Y - station.Origin.Y) != ' ';

        bool IsWalkable(WorldPosition position)
        {
            if (!Fits(position, 1, 1, levels))
                return false;
            if (OnStation(position))
                return Glyph(position.X - station.Origin.X, position.Y - station.Origin.Y) is '▢' or 'T';
            var level = levels[position.LevelId];
            return WorldContent.CreateCell(level.Rows[position.Y][position.X],
                level.Cells.FirstOrDefault(cell => cell.X == position.X && cell.Y == position.Y),
                level.Objects.Where(obj => obj.X == position.X && obj.Y == position.Y)).CanEnter([]);
        }
    }

    private static bool Fits(WorldPosition origin, int width, int height, IReadOnlyDictionary<Guid, LevelDefinition> levels) =>
        width > 0 && height > 0 && levels.TryGetValue(origin.LevelId, out var level) && level.Contains(origin.X, origin.Y) &&
        (long)origin.X + width <= level.Width && (long)origin.Y + height <= level.Height;

    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
