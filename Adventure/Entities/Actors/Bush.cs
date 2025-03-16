using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class Bush : Actor
    {
        public override string Name { get; set; } = "Сухой кустарник";
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
            switch (random.Next(0, 5))
            {
                case 0:
                    Console.WriteLine(message + "Кустарник.");
                    break;
                case 1:
                    Console.WriteLine(message + "Сухой куст.");
                    break;
                case 2:
                    Console.WriteLine(message + "Мертвый куст.");
                    break;
                case 3:
                    Console.WriteLine(message + "Куст.");
                    break;
                case 4:
                    Console.WriteLine(message + "Сухостой.");
                    break;
                case 5:
                    Console.WriteLine(message + "Мы нашли жизнь! А, мы ее давно нашли...");
                    break;
                default:
                    Console.WriteLine(message + "Кустарник.");
                    break;
            }
        }
    }
}
