using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class Border : Actor
    {
        public override string Name { get; set; } = "Граница карты";
        public override string Icon { get; set; } = "I";
        public override bool IsTakeble { get; set; } = false;
        public override bool IsCollision { get; set; } = true;
        public override ConsoleColor Color { get; set; } = ConsoleColor.DarkGray;
        public override Item? Inventory { get; set; } = null;

        public override void Action() { }
    }
}
