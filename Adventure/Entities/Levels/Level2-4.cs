using Adventure.Entities.Maps;

namespace Adventure.Entities.Levels
{
    public class Level2_4 : Level
    {
        public Level2_4() { }

        public override string Name { get; set; } = "N24S58";

        public override Level? TopLevel { get; set; }
        public override Level? BottomLevel { get; set; }
        public override Level? LeftLevel { get; set; }
        public override Level? RightLevel { get; set; }

        public override ConsoleColor LevelColor { get; set; } = ConsoleColor.Black;

        private Map map = new()
        {
            Width = 98,
            Height = 30,
            Cells = GenerateMap(Sprites.Level2_4Sprite)
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
            Console.WriteLine("Квадрант " + Name);
            Console.WriteLine("Инвентарь: ");
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
