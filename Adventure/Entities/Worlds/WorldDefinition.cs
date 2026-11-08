using System.Text.Json;

namespace Adventure.Entities.Worlds
{
    public sealed record WorldDefinition
    {
        public required int SchemaVersion { get; init; }
        public required Guid Id { get; init; }
        public required DroneStart Start { get; init; }
        public required List<LevelDefinition> Levels { get; init; }
        public List<BuildingDefinition> Buildings { get; init; } = [];
        public Dictionary<string, JsonElement> Metadata { get; init; } = [];
    }

    public sealed record DroneStart
    {
        public required Guid LevelId { get; init; }
        public required int X { get; init; }
        public required int Y { get; init; }
    }

    public sealed record LevelDefinition
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required int GlobalX { get; init; }
        public required int GlobalY { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required List<string> Rows { get; init; }
        public Dictionary<Vector, Guid> Neighbors { get; init; } = [];
        public ConsoleColor Color { get; init; } = ConsoleColor.Black;
        public List<ObjectDefinition> Objects { get; init; } = [];
        public List<CellProperties> Cells { get; init; } = [];
        public Dictionary<string, JsonElement> Metadata { get; init; } = [];

        public bool Contains(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
    }

    public sealed record ObjectDefinition
    {
        public required string Id { get; init; }
        public required ObjectKind Kind { get; init; }
        public required int X { get; init; }
        public required int Y { get; init; }
        public ItemKind? Item { get; init; }
        public ItemKind? RequiredItem { get; init; }
        public string? Name { get; init; }
        public string? Message { get; init; }
        public ConsoleColor? Color { get; init; }
        public bool IsAlien { get; init; }
        public bool CompletesGame { get; init; }
    }

    public enum ObjectKind { Chest, Terminal, ForceField, Sign }

    public enum ItemKind { HardDriveXD23, HardDriveXD23Full, ArtifactY66, UniversalPass, Key }

    public sealed record CellProperties
    {
        public required int X { get; init; }
        public required int Y { get; init; }
        public string? BuildingId { get; init; }
        public string? RoomId { get; init; }
        public bool? BlocksMovement { get; init; }
        public bool? BlocksScanning { get; init; }
    }

    public sealed record BuildingDefinition
    {
        public required string Id { get; init; }
        public List<string> Rooms { get; init; } = [];
    }
}
