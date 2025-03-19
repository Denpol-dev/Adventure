using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class ForceField : Actor
    {
        public ForceField(string field)
        {
            switch (field)
            {
                case "≈":
                    Pass = new ArtifactY66();
                    Color = ConsoleColor.Blue;
                    break;
                case "*":
                    Pass = new UniversalPass();
                    Color = ConsoleColor.Red;
                    break;
                default:
                    Pass = new Key();
                    break;
            }

        }

        public override string Name { get; set; } = "Силовое поле";
        public override string Icon { get; set; } = "≈";
        public override bool IsTakeble { get; set; } = false;
        public override bool IsCollision { get; set; } = true;
        public override ConsoleColor Color { get; set; } = ConsoleColor.White;
        public override Item? Inventory { get; set; } = null;
        public Item Pass { get; set; }

        public override void Action()
        {
            Console.SetCursorPosition(MessageX, MessageY);
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");
            string message = "Сообщение: ";
            Console.WriteLine(message + "Хм, получилось пройти. И этот артефакт начинает немного вибрировать...");
        }
    }
}
