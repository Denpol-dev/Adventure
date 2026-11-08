using Adventure.Entities.Maps;
using Adventure.Entities.Worlds;

namespace Adventure.Entities.Levels
{
    public sealed class Level
    {
        private readonly Dictionary<Vector, Level> _neighbors = [];

        internal Level(LevelDefinition definition)
        {
            Id = definition.Id;
            Name = definition.Name;
            GlobalX = definition.GlobalX;
            GlobalY = definition.GlobalY;
            Color = definition.Color;
            Map = new Map(definition);
        }

        public Guid Id { get; }
        public string Name { get; }
        public int GlobalX { get; }
        public int GlobalY { get; }
        public ConsoleColor Color { get; }
        public Map Map { get; }

        public Level? GetNeighbor(Vector direction) => _neighbors.GetValueOrDefault(direction);

        internal void Connect(Vector direction, Level neighbor) => _neighbors.Add(direction, neighbor);
    }
}
