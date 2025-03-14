using Adventure.Entities.Actors;

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
        public abstract void LoadMap();

        public static List<Cell> GenerateMap(string sprite)
        {
            var map = new List<Cell>();
            int stringIndex = 0;
            string cellString = "";
            int x = -1;
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
                        "#" => new Stone(),
                        "V" => new Bush(),
                        "." => new Sand(),
                        _ => new Chest(cellString),
                    };
                    map.Add(new Cell()
                    {
                        X = x,
                        Y = y,
                        CellType = new CellType()
                        {
                            Fill = cellString,
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
