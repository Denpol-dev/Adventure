using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class Sign : Actor
    {
        public Sign(string inscription)
        {
            Inscription = inscription;
        }

        public override string Name { get; set; } = "Табличка";
        public override bool IsTakeble { get; set; } = false;
        public override bool IsCollision { get; set; } = false;
        public override ConsoleColor Color { get; set; } = ConsoleColor.Green;
        public override Item? Inventory { get; set; } = null;

        public string Inscription { get; set; } = "";

        public override void Action()
        {
            Console.SetCursorPosition(0, 2);
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");
            string message = "Сообщение: ";
            Console.Write(message + "На табличке написано: " + Inscription);
        }
    }
}
