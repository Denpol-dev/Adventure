using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class Chest : Actor
    {
        public Chest(string chest)
        {
            switch (chest)
            {
                case "▣":
                    Inventory = new HardDriveXD23();
                    break;
                case "◊":
                    Inventory = new ArtifactY66();
                    break;
                case ";":
                    Inventory = new UniversalPass();
                    break;
                default:
                    Inventory = null;
                    break;
            }
        }

        public override string Name { get; set; } = "Ящик";
        public override string Icon { get; set; } = "▣";
        public override bool IsTakeble { get; set; } = true;
        public override bool IsCollision { get; set; } = false;
        public override ConsoleColor Color { get; set; } = ConsoleColor.DarkGreen;
        public override Item? Inventory { get; set; }

        public override void Action()
        {
            Console.SetCursorPosition(MessageX, MessageY);
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");
            string message = "Сообщение: ";
            if (Inventory != null)
            {
                Console.WriteLine(message + "Ящик открыт. Содержимое ящика \"" + Inventory.Name + "\" перенесено в инвентарь");
            }
            else
            {
                Console.WriteLine(message + "Пустой ящик.");
            }
        }
    }
}
