namespace Adventure.Entities.Levels
{
    public class Level2_1 : Level
    {
        public override string Name { get; set; } = "Заправка";

        private Level? topLevel = new Level1_1();
        public override Level? TopLevel
        {
            get => topLevel;
            set => topLevel = value;
        }

        private Level? bottomLevel = new Level2_1();
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

        private Level? rightLevel = new Level1_2();
        public override Level? RightLevel
        {
            get => rightLevel;
            set => rightLevel = value;
        }

        private Map map = new()
        {
            Width = 100,
            Height = 100,
            Cells = GenerateMap()
        };

        public override Map Map
        {
            get => map;
            set => map = value;
        }

        public static List<Cell> GenerateMap()
        {
            var map = new List<Cell>();
            for (int y = 0; y < 100; y++)
            {
                for (int x = 0; x < 100; x++)
                {

                }
            }
            return map;
        }
    }
}
