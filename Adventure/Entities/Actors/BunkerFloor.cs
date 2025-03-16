using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class BunkerFloor : Actor
    {
        public override string Name { get; set; } = "Пол бункера";
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
                    Console.WriteLine(message + "Пол бункера.");
                    break;
                case 1:
                    Console.WriteLine(message + "Бетон.");
                    break;
                case 2:
                    Console.WriteLine(message + "Бетонный пол.");
                    break;
                case 3:
                    Console.WriteLine(message + "Серый бетон.");
                    break;
                case 4:
                    Console.WriteLine(message + "Старый бетонный пол.");
                    break;
                case 5:
                    Console.WriteLine(message + "Хм, что-то странное... А, нет, показалось. Просто трещина на полу.");
                    break;
                default:
                    Console.WriteLine(message + "Бункерный пол.");
                    break;
            }
        }
    }
}
