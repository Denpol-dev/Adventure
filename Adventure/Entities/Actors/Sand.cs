using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class Sand : Actor
    {
        public override string Name { get; set; } = "Песок";
        public override bool IsTakeble { get; set; } = false;
        public override ConsoleColor Color { get; set; } = ConsoleColor.DarkYellow;
        public override Item? Inventory { get; set; }

        public override void Action()
        {
            Console.SetCursorPosition(MessageX, MessageY);
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");
            Console.WriteLine("Сообщение: ");
        }
    }
}
