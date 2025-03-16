using Adventure.Entities.Maps;

namespace Adventure.Entities.Levels
{
    public class Level4_4 : Level
    {
        public Level4_4() { }

        public override string Name { get; set; } = "N26S58";

        public override Level? TopLevel { get; set; }
        public override Level? BottomLevel { get; set; }
        public override Level? LeftLevel { get; set; }
        public override Level? RightLevel { get; set; }

        public override ConsoleColor LevelColor { get; set; } = ConsoleColor.Black;

        private Map map = new()
        {
            Width = 98,
            Height = 30,
            Cells = GenerateMap(Sprites.Level4_4Sprite)
        };

        public override Map GetMap()
        {
            return map;
        }

        public override void SetMap(Map value)
        {
            map = value;
        }
    }
}
