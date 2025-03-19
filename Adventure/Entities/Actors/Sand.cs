using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class Sand : Actor
    {
        public override string Name { get; set; } = "Песок";
        public override string Icon { get; set; } = ".";
        public override bool IsTakeble { get; set; } = false;
        public override bool IsCollision { get; set; } = false;
        public override ConsoleColor Color { get; set; } = ConsoleColor.DarkYellow;
        public override Item? Inventory { get; set; }

        public override void Action()
        {
            Console.SetCursorPosition(MessageX, MessageY);
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");
            Random random = new();
            string message = "Сообщение: "; 
            switch (random.Next(0, 2))
            {
                case 0:
                    Console.WriteLine(message + "Это песок.");
                    break;
                case 1:
                    Console.WriteLine(message + "Просто песок.");
                    break;
                case 2:
                    Console.WriteLine(message + "Сухой песок.");
                    break;
                default:
                    Console.WriteLine(message + "Песок.");
                    break;
            }
        }
    }
}
