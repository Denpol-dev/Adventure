namespace Adventure.Entities.Actors
{
    public class Sand :Actor
    {
        private string name = "Песок";
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
            Console.WriteLine("Сообщение: ");
        }
    }
}
