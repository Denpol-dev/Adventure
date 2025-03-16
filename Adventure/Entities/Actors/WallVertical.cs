using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class WallVertical : Actor
    {
        public override string Name { get; set; } = "Вертикальная стена";
        public override bool IsTakeble { get; set; } = false;
        public override bool IsCollision { get; set; } = true;
        public override ConsoleColor Color { get; set; } = ConsoleColor.Gray;
        public override Item? Inventory { get; set; } = null;

        public override void Action() { }
    }
}
