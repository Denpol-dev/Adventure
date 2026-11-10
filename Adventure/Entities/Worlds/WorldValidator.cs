using Adventure.Expeditions;

namespace Adventure.Entities.Worlds
{
    public static class WorldValidator
    {
        public const int SchemaVersion = 1;

        public static void Validate(WorldDefinition world)
        {
            Require(world.SchemaVersion == SchemaVersion, $"Неподдерживаемая версия мира: {world.SchemaVersion}.");
            Require(world.Id != Guid.Empty, "Не задан идентификатор мира.");
            Require(world.Start != null, "Не задана стартовая позиция дрона.");
            Require(world.Levels is { Count: > 0 }, "В мире отсутствуют квадранты.");
            Require(world.Metadata != null && world.Buildings != null, "Некорректные метаданные или список зданий мира.");

            var levels = new Dictionary<Guid, LevelDefinition>();
            var coordinates = new HashSet<(bool, int, int)>();
            var objectIds = new HashSet<string>(StringComparer.Ordinal);
            var buildings = ValidateBuildings(world.Buildings);

            foreach (var level in world.Levels)
            {
                Require(level != null && level.Id != Guid.Empty && !string.IsNullOrWhiteSpace(level.Name),
                    "У квадранта отсутствует идентификатор или имя.");
                Require(levels.TryAdd(level.Id, level), $"Повторяющийся идентификатор квадранта: {level.Id}.");
                Require(coordinates.Add((level.Underground, level.GlobalX, level.GlobalY)),
                    $"Квадрант {level.Name}: глобальные координаты ({level.GlobalX}, {level.GlobalY}) уже заняты.");
                ValidateLevel(level, objectIds, buildings, world.Expedition?.Station);
            }

            foreach (var level in levels.Values)
            {
                foreach (var (direction, neighborId) in level.Neighbors)
                {
                    Require(Enum.IsDefined(direction), $"Квадрант {level.Name}: неизвестное направление {direction}.");
                    Require(levels.TryGetValue(neighborId, out var neighbor),
                        $"Квадрант {level.Name}: сосед {direction} с идентификатором {neighborId} не найден.");
                    var (dx, dy) = direction.Offset();
                    Require(neighbor.Underground == level.Underground &&
                        neighbor.GlobalX == (long)level.GlobalX + dx && neighbor.GlobalY == (long)level.GlobalY + dy,
                        $"Квадрант {level.Name}: координаты соседа {neighbor.Name} не соответствуют направлению {direction}.");
                    Require(neighbor.Neighbors.GetValueOrDefault(direction.Opposite()) == level.Id,
                        $"Квадранты {level.Name} и {neighbor.Name}: отсутствует обратная связь.");
                }
            }

            var start = world.Start;
            Require(levels.TryGetValue(start.LevelId, out var first),
                $"Стартовый квадрант {start.LevelId} не найден.");
            Require(!first.Underground && first.Contains(start.X, start.Y),
                $"Квадрант {first.Name}: стартовая позиция ({start.X}, {start.Y}) вне карты.");
            var cell = WorldContent.CreateCell(first.Rows[start.Y][start.X],
                first.Cells.FirstOrDefault(cell => cell.X == start.X && cell.Y == start.Y),
                first.Objects.Where(obj => obj.X == start.X && obj.Y == start.Y));
            Require(cell.CanEnter([]), $"Квадрант {first.Name}: стартовая клетка непроходима.");
            if (world.Expedition is { } expedition)
            {
                ExpeditionValidator.Validate(expedition, levels, buildings);
                TerminalValidator.Validate(levels);
                var position = expedition.Drones.Single(drone => drone.Id == expedition.StartDroneId).Position;
                Require((position.LevelId, position.X, position.Y) == (start.LevelId, start.X, start.Y),
                    "Стартовая позиция мира не совпадает с позицией начального дрона.");
            }
        }

        private static Dictionary<string, HashSet<string>> ValidateBuildings(List<BuildingDefinition> buildings)
        {
            var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var building in buildings)
            {
                Require(building != null && !string.IsNullOrWhiteSpace(building.Id) && building.Rooms != null,
                    "Некорректное описание здания.");
                var rooms = new HashSet<string>(StringComparer.Ordinal);
                Require(result.TryAdd(building.Id, rooms), $"Повторяющийся идентификатор здания: {building.Id}.");
                foreach (var room in building.Rooms)
                    Require(!string.IsNullOrWhiteSpace(room) && rooms.Add(room),
                        $"Здание {building.Id}: пустой или повторяющийся идентификатор помещения '{room}'.");
            }
            return result;
        }

        private static void ValidateLevel(LevelDefinition level, HashSet<string> objectIds,
            Dictionary<string, HashSet<string>> buildings, StationDefinition? station)
        {
            Require(level.Width > 0 && level.Height > 0 && level.Rows?.Count == level.Height &&
                level.Rows.All(row => row?.Length == level.Width),
                $"Квадрант {level.Name}: строки не соответствуют размерам {level.Width} × {level.Height}.");
            Require(level.Neighbors != null && level.Objects != null && level.Cells != null && level.Metadata != null &&
                Enum.IsDefined(level.Color), $"Квадрант {level.Name}: некорректные параметры карты.");

            foreach (var obj in level.Objects)
            {
                Require(obj != null && !string.IsNullOrWhiteSpace(obj.Id),
                    $"Квадрант {level.Name}: у объекта отсутствует идентификатор.");
                Require(objectIds.Add(obj.Id), $"Повторяющийся идентификатор объекта: {obj.Id}.");
                Require(level.Contains(obj.X, obj.Y), $"Квадрант {level.Name}: объект {obj.Id} вне карты.");
                ValidateObject(level, obj);
            }

            var positions = new HashSet<(int, int)>();
            foreach (var cell in level.Cells)
            {
                Require(cell != null && level.Contains(cell.X, cell.Y), $"Квадрант {level.Name}: свойства клетки вне карты.");
                Require(positions.Add((cell.X, cell.Y)),
                    $"Квадрант {level.Name}: повторное описание клетки ({cell.X}, {cell.Y}).");
                if (cell.BuildingId is string buildingId)
                {
                    Require(buildings.TryGetValue(buildingId, out var rooms),
                        $"Квадрант {level.Name}: здание '{buildingId}' не найдено.");
                    Require(cell.RoomId == null || rooms.Contains(cell.RoomId),
                        $"Квадрант {level.Name}: помещение '{cell.RoomId}' не найдено в здании '{buildingId}'.");
                }
                else
                {
                    Require(cell.RoomId == null, $"Квадрант {level.Name}: у помещения '{cell.RoomId}' отсутствует здание.");
                }
            }

            var objects = level.Objects.ToLookup(obj => (obj.X, obj.Y));
            var personalGates = station?.Origin.LevelId == level.Id
                ? station.Bays?.Where(bay => bay != null).Select(bay => station.At(bay.GateX, bay.GateY)).ToHashSet()
                : null;
            for (int y = 0; y < level.Height; y++)
            {
                for (int x = 0; x < level.Width; x++)
                {
                    char symbol = level.Rows[y][x];
                    if (!WorldContent.IsKnownSymbol(symbol))
                        throw new InvalidDataException($"Квадрант {level.Name}: неизвестный символ '{symbol}' в ({x}, {y}).");
                    if (!WorldContent.HasRequiredObject(symbol, objects[(x, y)]) &&
                        !(symbol == '≈' && personalGates?.Contains(new(level.Id, x, y)) == true))
                        throw new InvalidDataException($"Квадрант {level.Name}: для символа '{symbol}' в ({x}, {y}) не задан объект нужного типа.");
                }
            }
        }

        private static void ValidateObject(LevelDefinition level, ObjectDefinition obj)
        {
            string location = $"Квадрант {level.Name}, объект {obj.Id}";
            Require(Enum.IsDefined(obj.Kind), $"{location}: неизвестный тип объекта {obj.Kind}.");
            Require(obj.Item == null || Enum.IsDefined(obj.Item.Value), $"{location}: неизвестный предмет {obj.Item}.");
            Require(obj.RequiredItem == null || Enum.IsDefined(obj.RequiredItem.Value),
                $"{location}: неизвестный требуемый предмет {obj.RequiredItem}.");
            Require(obj.Color == null || Enum.IsDefined(obj.Color.Value), $"{location}: неизвестный цвет {obj.Color}.");

            bool valid = obj.Kind switch
            {
                ObjectKind.Chest => obj.RequiredItem == null && obj.Message == null,
                ObjectKind.Terminal => (obj.Item == null) == (obj.RequiredItem == null) &&
                    (!obj.CompletesGame || obj.RequiredItem != null),
                ObjectKind.ForceField => obj.RequiredItem != null && obj.Item == null && obj.Message == null,
                ObjectKind.Sign => !string.IsNullOrWhiteSpace(obj.Message) && obj.Item == null && obj.RequiredItem == null,
                _ => false
            };
            Require(valid && (obj.Kind == ObjectKind.Terminal || (!obj.CompletesGame && !obj.IsAlien && obj.System == null)) &&
                (obj.System == null || obj.RequiredItem == null),
                $"{location}: некорректные параметры объекта типа {obj.Kind}.");
        }

        private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
        {
            if (!condition)
                throw new InvalidDataException(message);
        }
    }
}
