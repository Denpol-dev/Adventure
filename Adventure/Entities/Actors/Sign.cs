using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class Sign : Actor
    {
        public Sign(string sign)
        {
            string message = "На табличке написано: ";
            Inscription = sign switch
            {
                "©" => message + "Бункер HZ44B",
                _ => "Нечитабельная надпись",
            };
        }

        public override string Name { get; set; } = "Табличка";
        public override string Icon { get; set; } = "?";
        public override bool IsTakeble { get; set; } = false;
        public override bool IsCollision { get; set; } = false;
        public override ConsoleColor Color { get; set; } = ConsoleColor.Green;
        public override Item? Inventory { get; set; } = null;

        public string Inscription { get; set; }

        public override void Action()
        {
            Console.SetCursorPosition(0, 2);
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");
            Console.Write(Inscription);
        }
    }
}
