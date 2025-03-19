using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public abstract class Actor
    {
        public const int MessageX = 0;
        public const int MessageY = 2;
        public abstract string Name { get; set; }
        public abstract string Icon { get; set; }
        public abstract bool IsTakeble { get; set; }
        public abstract bool IsCollision { get; set; }
        public abstract ConsoleColor Color { get; set; }
        public abstract Item? Inventory { get; set; }
        public abstract void Action();
    }
}
