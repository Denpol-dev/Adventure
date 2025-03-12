namespace Adventure.Entities.Levels
{
    public abstract class Level
    {
        public abstract string Name { get; set; }

        public abstract Level? TopLevel { get; set; }
        public abstract Level? BottomLevel { get; set; }
        public abstract Level? LeftLevel { get; set; }
        public abstract Level? RightLevel { get; set; }

        public abstract Map Map { get; set; }
    }
}
