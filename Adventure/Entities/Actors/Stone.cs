namespace Adventure.Entities.Actors
{
    public class Stone : Actor
    {
        private string name = "Камень";
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

        public override ConsoleColor Color { get; set; } = ConsoleColor.DarkCyan;

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
