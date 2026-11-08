using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class CarFloor : Actor
    {
        public override string Name { get; set; } = "Пол бункера";
        public override string Icon { get; set; } = "□";
        public override bool IsTakeble { get; set; } = false;
        public override bool IsCollision { get; set; } = false;
        public override ConsoleColor Color { get; set; } = ConsoleColor.DarkYellow;
        public override Item? Inventory { get; set; }

        public override void Action()
        {
            ConsoleView.ClearLine(MessageY);
            string message = "Сообщение: ";
            Console.WriteLine(message + "Пол грузовика.");
        }
    }
}
