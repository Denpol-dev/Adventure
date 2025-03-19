using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class Stone : Actor
    {
        public override string Name { get; set; } = "Камень";
        public override string Icon { get; set; } = "#";
        public override bool IsTakeble { get; set; } = false;
        public override bool IsCollision { get; set; } = false;
        public override ConsoleColor Color { get; set; } = ConsoleColor.DarkGray;
        public override Item? Inventory { get; set; }

        public override void Action()
        {
            Console.SetCursorPosition(MessageX, MessageY);
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");
            Random random = new();
            string message = "Сообщение: ";
            switch (random.Next(0, 5))
            {
                case 0:
                    Console.WriteLine(message + "Это камень.");
                    break;
                case 1:
                    Console.WriteLine(message + "Просто камень.");
                    break;
                case 2:
                    Console.WriteLine(message + "Булыжник.");
                    break;
                case 3:
                    Console.WriteLine(message + "Серый камень.");
                    break;
                case 4:
                    Console.WriteLine(message + "Сборище минералов.");
                    break;
                case 5:
                    Console.WriteLine(message + "Это же... А, нет, просто камень.");
                    break;
                default:
                    Console.WriteLine(message + "Камень.");
                    break;
            }
        }
    }
}
