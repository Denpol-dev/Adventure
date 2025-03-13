namespace Adventure.Entities.Actors
{
    public class Bush : Actor
    {
        private string name = "Сухой кустарник";
        public override string Name
        {
            get => name;
            set => name = value;
        }

        private bool isTakeble = false;
        public override bool IsTakeble
        {
            get => isTakeble;
            set => isTakeble = value;
        }

        public override ConsoleColor Color { get; set; } = ConsoleColor.DarkYellow;

        public override void Action()
        {
            Console.ForegroundColor = ConsoleColor.Black;
            Console.SetCursorPosition(0, 0);
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");
            Random random = new();
            string message = "Сообщение: ";
            switch (random.Next(1, 5))
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
