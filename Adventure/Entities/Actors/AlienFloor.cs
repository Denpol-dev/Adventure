using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class AlienFloor : Actor
    {
        public override string Name { get; set; } = "Инопланетный пол";
        public override string Icon { get; set; } = "▧";
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
                    Console.WriteLine(message + "Инопланетный пол.");
                    break;
                case 1:
                    Console.WriteLine(message + "Странный материал.");
                    break;
                case 2:
                    Console.WriteLine(message + "Кажется, он немного пружинит.");
                    break;
                case 3:
                    Console.WriteLine(message + "Очень прочный камень. Или металл. Непонятно.");
                    break;
                case 4:
                    Console.WriteLine(message + "Пол покрыт пылью, но на нем ни трещинки.");
                    break;
                case 5:
                    Console.WriteLine(message + "Древний инопланетный пол");
                    break;
                default:
                    Console.WriteLine(message + "Инопланетный пол.");
                    break;
            }
        }
    }
}
