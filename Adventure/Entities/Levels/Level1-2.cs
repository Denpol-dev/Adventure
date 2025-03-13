namespace Adventure.Entities.Levels
{
    public class Level1_2 : Level
    {
        public override string Name { get; set; } = "Рынок вход";

        private Level? topLevel = null;
        public override Level? TopLevel
        {
            get => topLevel;
            set => topLevel = value;
        }

        private Level? bottomLevel = null;//new Level2_2();
        public override Level? BottomLevel
        {
            get => bottomLevel;
            set => bottomLevel = value;
        }

        private Level? leftLevel = new Level1_1();
        public override Level? LeftLevel
        {
            get => leftLevel;
            set => leftLevel = value;
        }

        private Level? rightLevel = null;//new Level1_3();
        public override Level? RightLevel
        {
            get => rightLevel;
            set => rightLevel = value;
        }

        private ConsoleColor levelColor = ConsoleColor.Gray;
        public override ConsoleColor LevelColor
        {
            get => levelColor;
            set => levelColor = value;
        }

        private Map map = new()
        {
            Width = 100,
            Height = 40,
            Cells = GenerateMap()
        };

        public override Map GetMap()
        {
            return map;
        }

        public override void SetMap(Map value)
        {
            map = value;
        }

        public static List<Cell> GenerateMap()
        {
            var map = new List<Cell>();
            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 100; x++)
                {
                    map.Add(new Cell()
                    {
                        X = x,
                        Y = y,
                        CellType = new CellType()
                        {

                        }
                    });
                }
            }
            return map;
        }

        public override void LoadMap()
        {
            throw new NotImplementedException();
        }
    }
}
