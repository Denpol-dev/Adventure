using Adventure.Entities.Actors;
using Adventure.Entities.Characters;
using Adventure.Entities.Maps;
using System.Text;

namespace Adventure.Entities.Levels
{
    public abstract class Level
    {
        public abstract string Name { get; set; }

        public abstract Level? TopLevel { get; set; }
        public abstract Level? BottomLevel { get; set; }
        public abstract Level? LeftLevel { get; set; }
        public abstract Level? RightLevel { get; set; }

        public abstract ConsoleColor LevelColor { get; set; }

        public void InitDirections(
            Level? topLevel,
            Level? bottomLevel,
            Level? leftLevel,
            Level? rightLevel)
        {
            TopLevel = topLevel;
            BottomLevel = bottomLevel;
            LeftLevel = leftLevel;
            RightLevel = rightLevel;
        }
        public abstract Map GetMap();
        public abstract void SetMap(Map value);

        public void LoadMap(Character character)
        {
            var map = GetMap();
            Console.WriteLine("Квадрант " + Name);
            LoadInventory(character);
            Console.WriteLine("Сообщение: ");
            foreach (var cell in map.Cells.Select(c => c.CellType))
            {
                var cellString = cell.Fill;
                if (cellString == "I")
                {
                    Console.ForegroundColor = cell.Actor?.Color ?? ConsoleColor.Gray;
                    Console.WriteLine(cell.Fill);
                }
                else
                {
                    Console.ForegroundColor = cell.Actor?.Color ?? ConsoleColor.Gray;
                    Console.Write(cell.Fill);
                }
            }
        }

        public static void LoadInventory(Character character)
        {
            if (character != null)
            {
                var sb = new StringBuilder();
                var inventory = character.Inventory.Items;
                if (inventory.Count > 0)
                {

                    foreach (var item in inventory)
                    {
                        sb.Append(item.Name + ", ");
                    }
                    sb.Length -= 2;
                    Console.WriteLine("Инвентарь: " + sb.ToString());
                }
                else
                {
                    Console.WriteLine("Инвентарь: ");
                }
            }
        }

        public static List<Cell> GenerateMap(string rawSprite)
        {
            string sprite = rawSprite.Replace("\r", "").Replace("\n", "");
            var map = new List<Cell>();
            int stringIndex = 0;
            string cellString = "";
            int x;
            int y = 2;
            while (cellString != "~")
            {
                stringIndex++;
                cellString = sprite[stringIndex].ToString();
                y++;
                x = 0;
                while (cellString != "I" && cellString != "~")
                {
                    cellString = sprite[stringIndex].ToString();
                    Actor? actor = cellString switch
                    {
                        "I" => null,
                        "_" => null,
                        "~" => null,
                        "#" => new Stone(),
                        "V" => new Bush(),
                        "." => new Sand(),
                        "+" => new WallCorner(),
                        "-" => new WallHorizontal(),
                        "|" => new WallVertical(),
                        "/" => new WallObliqueRight(),
                        @"\"=> new WallObliqueLeft(),
                        "▢" => new BunkerFloor(),
                        "8" => new Rock(),
                        "≈" => new ForceField(cellString),
                        "*" => new ForceField(cellString),
                        "▧" => new AlienFloor(),
                        "▮" => new Table(),
                        "□" => new CarFloor(),

                        "©" => new Sign(cellString),

                        "1" => new Terminal(cellString),
                        "2" => new Terminal(cellString),
                        "3" => new Terminal(cellString),
                        "4" => new Terminal(cellString),
                        "5" => new Terminal(cellString),
                        "6" => new Terminal(cellString),
                        "7" => new Terminal(cellString),
                        "9" => new Terminal(cellString),
                        "0" => new Terminal(cellString),
                        "◬" => new Terminal(cellString),
                        "◮" => new Terminal(cellString),

                        "▣" => new Chest(cellString),
                        "◊" => new Chest(cellString),
                        ";" => new Chest(cellString),
                        "e" => new Chest(cellString),
                        _ => null
                    };
                    map.Add(new Cell()
                    {
                        X = x,
                        Y = y,
                        CellType = new CellType()
                        {
                            Fill = actor?.Icon ?? cellString,
                            Actor = actor
                        }
                    });
                    stringIndex++;
                    x++;
                }
            }

            return map;
        }
    }
}
