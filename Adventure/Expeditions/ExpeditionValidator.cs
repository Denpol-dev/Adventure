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
        Require(definition.Drones is { Count: > 0 and <= 16 } && definition.Donors != null && definition.Interiors != null &&
            definition.LayoutRevision >= 0,
            "Некорректный парк экспедиции.");

        var station = definition.Station;
        Require(station != null && !string.IsNullOrWhiteSpace(station.Id) && station.Bays != null &&
            !string.IsNullOrWhiteSpace(station.ServiceTerminalId), "Не задана ремонтная станция.");
        Require(Fits(station.Origin, station.Width, station.Height, levels), "Ремонтная станция выходит за пределы квадранта.");
        var stationLevel = levels[station.Origin.LevelId];
        var service = stationLevel.Objects.SingleOrDefault(obj => obj.Id == station.ServiceTerminalId);
        Require(IsReadOnlyTerminal(service) && OnStation(new(stationLevel.Id, service.X, service.Y)) &&
            stationLevel.Rows[service.Y][service.X] == '░', "Не задан терминал обслуживания станции.");
        int gateCount = 0;
        for (int y = 0; y < station.Height; y++)
        for (int x = 0; x < station.Width; x++)
        {
            if (Glyph(x, y) == '≈')
                gateCount++;
        }
        Require(gateCount == station.Bays.Count, "Число персональных полей не совпадает с числом боксов.");

        var drones = new Dictionary<string, DroneDefinition>(StringComparer.Ordinal);
        var colors = new HashSet<ConsoleColor>();
        var occupied = new HashSet<WorldPosition>();
        var components = new HashSet<string>(StringComparer.Ordinal);
        foreach (var drone in definition.Drones)
        {
            Require(drone != null && !string.IsNullOrWhiteSpace(drone.Id) && !string.IsNullOrWhiteSpace(drone.Name) &&
                drones.TryAdd(drone.Id, drone) && Enum.IsDefined(drone.Kind) && Enum.IsDefined(drone.Color) &&
                colors.Add(drone.Color) && drone.MaxWeight > 0 && drone.BatteryPercent is > 0 and <= 100 &&
                IsWalkable(drone.Position) && !OnStation(drone.Position) && occupied.Add(drone.Position), "Некорректное описание управляемого дрона.");
            foreach (var slot in new[] { EquipmentSlot.Locomotion, EquipmentSlot.LeftArm, EquipmentSlot.RightArm, EquipmentSlot.Battery })
                components.Add($"{drone.Id}/{slot}");
        }
        Require(drones.ContainsKey(definition.StartDroneId) && station.Bays.Count == drones.Count,
            "Начальный дрон или парковочные боксы не заданы.");

        var start = drones[definition.StartDroneId].Position;
        var entrance = start with { Y = start.Y - 1 };
        Require(start.LevelId == station.Origin.LevelId && start.Y == station.Origin.Y + station.Height &&
            OnStation(entrance) && stationLevel.Rows[entrance.Y][entrance.X] == '▢',
            "Начальный дрон должен находиться перед южным входом станции.");

        var bayIds = new HashSet<string>(StringComparer.Ordinal);
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        var bayCells = new HashSet<(int, int)>();
        foreach (var bay in station.Bays)
        {
            Require(bay != null && !string.IsNullOrWhiteSpace(bay.Id) && bayIds.Add(bay.Id) &&
                drones.ContainsKey(bay.DroneId) && assigned.Add(bay.DroneId) &&
                Glyph(bay.GateX, bay.GateY) == '≈' && Glyph(bay.ParkingX, bay.ParkingY) == '▢' &&
                bay.ParkingY == bay.GateY && Math.Abs(bay.ParkingX - bay.GateX) == 1 &&
                Glyph(bay.TerminalX, bay.ParkingY) == '░', "Некорректная геометрия парковочного бокса.");
            int sign = Math.Sign(bay.ParkingX - bay.GateX);
            Require(IsWall(Glyph(bay.TerminalX + sign, bay.ParkingY)) &&
                Glyph(bay.GateX - sign, bay.GateY) == '▢', $"Некорректный вход в бокс {bay.Id}.");
            foreach (int x in new[] { bay.GateX, bay.ParkingX, bay.TerminalX })
            {
                Require(bayCells.Add((x, bay.ParkingY)) && IsWall(Glyph(x, bay.ParkingY - 1)) &&
                    IsWall(Glyph(x, bay.ParkingY + 1)), $"Перегородки бокса {bay.Id} не замкнуты.");
            }

            var terminal = station.At(bay.TerminalX, bay.ParkingY);
            var gate = station.At(bay.GateX, bay.GateY);
            Require(!stationLevel.Objects.Any(obj => obj.X == gate.X && obj.Y == gate.Y) &&
                IsReadOnlyTerminal(stationLevel.Objects.FirstOrDefault(obj =>
                    obj.X == terminal.X && obj.Y == terminal.Y && obj.Id != service.Id)),
                $"Некорректные объекты бокса {bay.Id}.");
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
                Require(rooms.Add(point), "Области помещений пересекаются.");
            }
        }

        char Glyph(int x, int y) => x >= 0 && y >= 0 && x < station.Width && y < station.Height
            ? stationLevel.Rows[station.Origin.Y + y][station.Origin.X + x]
            : ' ';
        bool OnStation(WorldPosition position) => station.Contains(position) &&
            StationDefinition.IsStationGlyph(stationLevel.Rows[position.Y][position.X]);

        bool IsWalkable(WorldPosition position)
        {
            if (!Fits(position, 1, 1, levels))
                return false;
            var level = levels[position.LevelId];
            return WorldContent.CreateCell(level.Rows[position.Y][position.X],
                level.Cells.FirstOrDefault(cell => cell.X == position.X && cell.Y == position.Y),
                level.Objects.Where(obj => obj.X == position.X && obj.Y == position.Y)).CanEnter([]);
        }
    }

    private static bool IsWall(char glyph) => glyph is '+' or '-' or '|';

    private static bool IsReadOnlyTerminal(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] ObjectDefinition? obj) =>
        obj is { Kind: ObjectKind.Terminal, RequiredItem: null, Item: null, IsAlien: false, CompletesGame: false };

    private static bool Fits(WorldPosition origin, int width, int height, IReadOnlyDictionary<Guid, LevelDefinition> levels) =>
        width > 0 && height > 0 && levels.TryGetValue(origin.LevelId, out var level) && level.Contains(origin.X, origin.Y) &&
        (long)origin.X + width <= level.Width && (long)origin.Y + height <= level.Height;

    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
