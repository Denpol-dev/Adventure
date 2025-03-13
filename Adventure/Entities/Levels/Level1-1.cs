using Adventure.Entities.Maps;

namespace Adventure.Entities.Levels
{
    public class Level1_1 : Level
    {
        public override string Name { get; set; } = "Каменная пустыня";

        private Level? topLevel = null;
        public override Level? TopLevel
        {
            get => topLevel;
            set => topLevel = value;
        }

        private Level? bottomLevel = null;//new Level2_1();
        public override Level? BottomLevel
        {
            get => bottomLevel;
            set => bottomLevel = value;
        }

        private Level? leftLevel = null;
        public override Level? LeftLevel
        {
            get => leftLevel;
            set => leftLevel = value;
        }

        private Level? rightLevel = null;//new Level1_2();
        public override Level? RightLevel
        {
            get => rightLevel;
            set => rightLevel = value;
        }

        public override ConsoleColor LevelColor { get; set; } = ConsoleColor.DarkGray;

        private Map map = new()
        {
            Width = 98,
            Height = 28,
            Cells = GenerateMap(Sprites.Level1_1Sprite)
        };

        public override Map GetMap()
        {
            return map;
        }

        public override void SetMap(Map value)
        {
            map = value;
        }

        public override void LoadMap()
        {
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine("Сообщение: ");
            foreach (var cell in map.Cells.Select(c => c.CellType))
            {
                var cellString = cell.Fill;
                if (cellString == "I")
                {
                    Console.ForegroundColor = cell.Actor?.Color ?? ConsoleColor.Black;
                    Console.WriteLine(cell.Fill);
                }
                else
                {
                    Console.ForegroundColor = cell.Actor?.Color ?? ConsoleColor.Black;
                    Console.Write(cell.Fill);
                }
            }
        }
    }
}
