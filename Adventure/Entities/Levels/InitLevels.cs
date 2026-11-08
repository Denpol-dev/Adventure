using Adventure.Entities.Worlds;

namespace Adventure.Entities.Levels
{
    public sealed class InitLevels
    {
        public InitLevels(WorldDefinition definition)
        {
            WorldValidator.Validate(definition);
            var levels = definition.Levels.ToDictionary(level => level.Id, level => new Level(level));
            foreach (var level in definition.Levels)
            {
                foreach (var (direction, neighbor) in level.Neighbors)
                    levels[level.Id].Connect(direction, levels[neighbor]);
            }

            Levels = levels;
            Start = definition.Start;
            FirstLevel = levels[Start.LevelId];
        }

        public IReadOnlyDictionary<Guid, Level> Levels { get; }
        public DroneStart Start { get; }
        public Level FirstLevel { get; }
    }
}
