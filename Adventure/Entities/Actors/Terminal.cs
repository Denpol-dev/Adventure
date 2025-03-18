using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class Terminal : Actor
    {
        public Terminal(string terminal)
        {
            switch (terminal)
            {
                case "¶":
                    Message = "Не волнуйся, я скоро буду дома, дорогая. Нам не рассказывают, что произошло, но мы вывозим отсюда практически все, что под руку попадается. Через 40 минут погрузка, а там каких-то два года и я в родной системе.";
                    Name = "Терминал 2547";
                    break;
                default:
                    Message = "...";
                    Name = "...";
                    break;
            }
        }

        public override string Name { get; set; }
        public override bool IsTakeble { get; set; } = false;
        public override bool IsCollision { get; set; } = false;
        public override ConsoleColor Color { get; set; } = ConsoleColor.Magenta;
        public override Item? Inventory { get; set; } = null;
        public string Message { get; set; }

        public override void Action()
        {
            Console.SetCursorPosition(0, 2);
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");
            string message = Name + ": ";
            Console.Write(message + Message);
        }
    }
}
