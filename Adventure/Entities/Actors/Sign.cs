using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class Sign : Actor
    {
        public override string Name { get; set; } = "Табличка";
        public override string Icon { get; set; } = "?";
        public override bool IsTakeble { get; set; } = false;
        public override bool IsCollision { get; set; } = false;
        public override ConsoleColor Color { get; set; } = ConsoleColor.Green;
        public override Item? Inventory { get; set; } = null;

        public required string Inscription { get; init; }

        public override void Action()
        {
            ConsoleView.ClearLine(MessageY);
            Console.Write(Inscription);
        }
    }
}
