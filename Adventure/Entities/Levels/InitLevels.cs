using Newtonsoft.Json;
using System.IO;

namespace Adventure.Entities.Levels
{
    public class InitLevels
    {
        public Level FirstLevel { get; set; }

        public Source Source { get; set; }

        public InitLevels()
        {
            Source = JsonConvert.DeserializeObject<Source>(File.ReadAllText(@"Sources\source.json")) ?? throw new Exception("Произошла ошибка при чтении файла");

            var levels = Source.Levels;

            foreach (var level in levels)
            {
                level.InitDirections(
                    levels.FirstOrDefault(l => l.LevelId == level.TopLevelId),
                    levels.FirstOrDefault(l => l.LevelId == level.BottomLevelId),
                    levels.FirstOrDefault(l => l.LevelId == level.LeftLevelId),
                    levels.FirstOrDefault(l => l.LevelId == level.RightLevelId));
                level.GenerateMap();
            }

            FirstLevel = Source.Levels.First(l=> l.GlobalX == Source.StartX && l.GlobalY == Source.StartY);
        }
    }

    public class Source
    {
        public int StartX { get; set; }
        public int StartY { get; set; }
        public List<Level> Levels { get; set; }
    }
}
