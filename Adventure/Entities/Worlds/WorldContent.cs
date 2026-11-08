using Adventure.Entities.Actors;
using Adventure.Entities.Items;
using Adventure.Entities.Maps;

namespace Adventure.Entities.Worlds
{
    internal static class WorldContent
    {
        private static readonly Dictionary<char, Func<Actor>> TerrainActors = new()
        {
            ['.'] = () => new Sand(),
            ['#'] = () => new Stone(),
            ['V'] = () => new Bush(),
            ['+'] = () => new WallCorner(),
            ['-'] = () => new WallHorizontal(),
            ['|'] = () => new WallVertical(),
            ['/'] = () => new WallObliqueRight(),
            ['\\'] = () => new WallObliqueLeft(),
            ['▢'] = () => new BunkerFloor(),
            ['8'] = () => new Rock(),
            ['▧'] = () => new AlienFloor(),
            ['▮'] = () => new Table(),
            ['□'] = () => new CarFloor()
        };

        private static ObjectKind? MarkerKind(char symbol) => symbol switch
        {
            '≈' or '*' => ObjectKind.ForceField,
            '▣' or '◊' or ';' or 'e' => ObjectKind.Chest,
            '1' or '2' or '3' or '4' or '5' or '6' or '7' or '9' or '0' or '◬' or '◮' => ObjectKind.Terminal,
            '©' => ObjectKind.Sign,
            _ => null
        };

        public static bool IsKnownSymbol(char symbol) =>
            TerrainActors.ContainsKey(symbol) || symbol is ' ' or '◉' || MarkerKind(symbol) != null;

        public static bool HasRequiredObject(char symbol, IEnumerable<ObjectDefinition> objects) =>
            MarkerKind(symbol) is not ObjectKind kind || objects.Any(obj => obj.Kind == kind);

        public static Cell CreateCell(char symbol, CellProperties? properties, IEnumerable<ObjectDefinition> objects)
        {
            var terrain = TerrainActors.TryGetValue(symbol, out var create) ? create() : null;
            return new Cell(symbol, terrain, properties,
                objects.Select(obj => new PlacedObject(obj.Id, CreateActor(obj))).ToArray());
        }

        private static Actor CreateActor(ObjectDefinition obj)
        {
            var item = CreateItem(obj.Item);
            var requiredItem = CreateItem(obj.RequiredItem);
            Actor actor = obj.Kind switch
            {
                ObjectKind.Chest => new Chest { Inventory = item },
                ObjectKind.Terminal => new Terminal
                {
                    Name = obj.Name ?? "...",
                    Message = obj.Message ?? "...",
                    LoadItem = requiredItem,
                    Inventory = item,
                    IsAlien = obj.IsAlien,
                    CompletesGame = obj.CompletesGame
                },
                ObjectKind.ForceField => new ForceField(requiredItem!),
                ObjectKind.Sign => new Sign { Inscription = obj.Message! },
                _ => throw new InvalidDataException($"Неизвестный тип объекта {obj.Kind}.")
            };

            if (obj.Color is ConsoleColor color)
                actor.Color = color;
            if (obj.Name is string name)
                actor.Name = name;

            return actor;
        }

        internal static Item? CreateItem(ItemKind? kind) => kind switch
        {
            null => null,
            ItemKind.HardDriveXD23 => new HardDriveXD23(),
            ItemKind.HardDriveXD23Full => new HardDriveXD23Full(),
            ItemKind.ArtifactY66 => new ArtifactY66(),
            ItemKind.UniversalPass => new UniversalPass(),
            ItemKind.Key => new Key(),
            _ => throw new InvalidDataException($"Неизвестный предмет {kind}.")
        };

        internal static ItemKind GetItemKind(Item item) => item switch
        {
            HardDriveXD23 => ItemKind.HardDriveXD23,
            HardDriveXD23Full => ItemKind.HardDriveXD23Full,
            ArtifactY66 => ItemKind.ArtifactY66,
            UniversalPass => ItemKind.UniversalPass,
            Key => ItemKind.Key,
            _ => throw new InvalidDataException($"Неизвестный предмет {item.Name}.")
        };
    }
}
